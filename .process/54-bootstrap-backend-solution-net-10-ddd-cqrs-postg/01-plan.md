# Plan — Bootstrap backend solution (.NET 10, DDD/CQRS, PostgreSQL) · Story #54 [E1.S1]

## Goal
A developer can clone the repo, start PostgreSQL with `docker compose up -d postgres`, apply the initial EF Core migration, run `Elmanhg.Api`, and get `200 Healthy` from `GET /health` plus OpenAPI/Scalar docs. `dotnet build api/` and `dotnet test api/` pass, and CI runs restore, build, test, the migration check and the vulnerable-package gate on every PR. Later stories get a working Api / Application / Domain / Infrastructure / Tests skeleton on the vendored `Core.*` libraries, with Npgsql in place of SQL Server.

## Scope
**In:**
- Vendor Morabh `Core/*` (15 projects, **not** `Core.Azure`) into `api/core-libraries/`, with the SQL-Server-to-PostgreSQL swaps listed below.
- `api/Elmanhg.slnx` containing `Elmanhg.Api`, `Elmanhg.Application`, `Elmanhg.Domain`, `Elmanhg.Infrastructure` and `Elmanhg.Tests`.
- Central package management for `Elmanhg.*`. `core-libraries` opts out and keeps its inline versions.
- `AppDbContext : CoreDbContext<User, Role, Guid>` on Npgsql with `EnableRetryOnFailure()`.
- A `jsonb` column: the Core `Notification.Data` column is mapped as `jsonb`.
- An `InitialCreate` migration and a dotnet-ef tool manifest.
- MediatR pipeline from Core: `ValidationBehaviour` (Core.CQRS) and `AuditBehaviour` (Core.Auditing, off unless `CoreAuditing:Enabled`).
- `/health` via ASP.NET health checks with a DbContext check.
- OpenAPI (`Microsoft.AspNetCore.OpenApi`) and Scalar outside Production.
- Serilog structured logging via `Core.Logging` and `CoreRequestLoggingMiddleware`.
- Environment config: `.env.example`, a `.env` loader in Development, and `appsettings.example.json`.
- `docker-compose.yml` with a `pgvector/pgvector:pg17` service.
- `.github/workflows/api-ci.yml`.
- A Postman collection and environment.
- Tests: integration tests against Testcontainers PostgreSQL, and unit tests for the two vendored files this story changes behaviourally.
- A README section.

**Out:**
- Business entities and controllers.
- Auth wiring: `IdentityOptions`, `RoleNames`, login/register, `AddCoreOtp`, `AddCoreNotifications` and their endpoints. These belong to story #56. `AddCoreNotifications` also throws at startup without Firebase credentials.
- Logging and unit-of-work MediatR behaviours. Core.CQRS ships only `ValidationBehaviour`. Request logging is `CoreRequestLoggingMiddleware` (HTTP). The unit of work is the single `SaveChangesAsync` in each handler (SKILL §5.6). A UoW behaviour would contradict that rule.
- Swagger/Swashbuckle. SKILL §11 says to use OpenAPI + Scalar instead.

**Deferred:**
- `Elmanhg.Jobs`: SKILL delta 6 says "when needed". The constitution says Hangfire, while Morabh.Jobs is Azure Functions, so the first story that needs a job decides.
- `Application/Exceptions/ErrorCodes.cs`: comes with the first story that has an error code.
- `dotnet format` gate: Morabh's `.editorconfig` forces CRLF, which breaks on Linux CI. Needs its own decision.
- Coverage gate and gitleaks: not in the story checklist.
- pgvector `CREATE EXTENSION`: comes with the Avatar story.
- MediatR licence-key configuration.
- Respawn: no data-mutating tests yet.
- A `/health` 503 integration test. `EnableRetryOnFailure` makes an unreachable-DB probe take about 60 s, and the 503 path is framework code with no branch of ours.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Template (`D:\Personal\CoreLibrary\dotnet-templates`) or Morabh copy? | **Copy from Morabh**, with the Core projects kept separate. Borrow from the template: its fixed `ExceptionMiddleware.cs`, the CPM layout and core opt-out props, the Postman shape, the dotnet-tools manifest, the CI vuln-grep and the `Microsoft.OpenApi` pin. | The template merges Core into one `Core.csproj`. Morabh and SKILL §3 use separate `Core.*` projects, so the Morabh copy is closer. The command file also names `ddd-sln`, but the template's short name is `core-api`. |
| D2 | Which SQL Server dependencies need swapping? | (a) Infrastructure uses `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. (b) `Core.Logging`: remove `Serilog.Sinks.MSSqlServer`, `ConfigureSql`, `SqlLoggingOptions` and `SqlSinkTarget`. (c) `DateTimeOffset.Now` → `UtcNow` in `AuditEntity` and `Repository.SaveChangesAsync`. (d) Morabh's `SqlServerEventId` ignore is not carried over. | These were the only SQL Server references found. (c) is required because Npgsql rejects non-UTC `DateTimeOffset` for `timestamptz`, and the dev machine runs at +03:00. |
| D3 | Keep Core.Logging's Azure sinks (AzureTable, AppInsights)? | Keep them; they are disabled by default. | The instruction was to drop the `Core.Azure` *project*. The sinks are config-gated. Mirror, don't modernise. |
| D4 | How to show jsonb support? | In `CoreDbContext`, add `.HasColumnType("jsonb")` to `Notification.Data`. | This is the only JSON-shaped column in the plumbing. SKILL delta 2 forbids JSON in `text`. |
| D5 | "Initial empty migration"? | `InitialCreate`, containing the Identity and Core tables (Otps, UserDevices, Notifications, NotificationTemplates, AuditLogs) and no Elmanhg business tables. | `AppDbContext` must inherit `CoreDbContext` (SKILL §6.2), so a truly empty model is impossible. |
| D6 | `appsettings.json` committed? | **No.** Commit `api/Elmanhg.Api/appsettings.example.json` (full shape, safe defaults, empty secrets) and gitignore `appsettings.json`, `appsettings.Development.json` and `appsettings.Production.json`. The implementer creates a local `appsettings.json` copy that is not committed. | Constitution §2 ("never re-add appsettings.json") wins over SKILL delta 7. Morabh and the template gitignore it too. |
| D7 | Local secrets? | Repo-root `.env`, which is gitignored and shared by docker compose and the API. `.env.example` is committed. In Development, `Program.cs` calls `DotNetEnv.Env.TraversePath().Load()` and then `builder.Configuration.AddEnvironmentVariables()`. Morabh's `AddUserSecrets` is dropped. | Constitution §0.4 and SKILL delta 7. Loading only in Development keeps a developer's `.env` out of test and prod hosts. |
| D8 | JWT config is required even without auth endpoints? | Yes. `.env.example` and `ApiFactory` supply `CoreJwt:Issuer/Audience/Key`. | `AddCoreIdentity` sets JwtBearer as the default scheme. Its options delegate throws on every request if these are empty. |
| D9 | Identity options? | Call `AddCoreIdentity` **without** `identityOptions`. | Password and lockout rules belong to #56. `AddCoreIdentity` is also what registers `AppDbContext`. |
| D10 | Health endpoint shape | `AddHealthChecks().AddDbContextCheck<AppDbContext>()` and `MapHealthChecks("/health").AllowAnonymous()`. It returns `text/plain` `Healthy` (200) or `Unhealthy` (503). | Uses the framework and proves the DB is reachable. The template's minimal `MapGet` did not check the DB. |
| D11 | OpenAPI/Scalar exposure | Map only when `!app.Environment.IsProduction()`. Routes are `/openapi/v1.json` and `/scalar/v1`. | Morabh maps it unconditionally, which exposes docs in prod. Tests run in `Testing`, so they can assert it. |
| D12 | `Microsoft.OpenApi` | Add a direct pin at **2.7.5**. | Verified: `Microsoft.AspNetCore.OpenApi` 10.0.9 pulls in 2.0.0, which is High severity (GHSA-v5pm-xwqc-g5wc). With the pin the set scans clean. |
| D13 | Warnings | `api/Directory.Build.props` sets `TreatWarningsAsErrors=true` and `WarningsNotAsErrors=NU1901;NU1902;NU1903;NU1904`. `core-libraries` does **not** inherit it: its own empty `Directory.Build.props` stops the upward search. | Zero new warnings in Elmanhg code, without suppressing audit warnings (command rules forbid `NoWarn NU190x`). The CI vuln step is the audit gate. Vendored code keeps its existing warnings. |
| D14 | Test stack | xUnit v3 `xunit.v3.mtp-v2` 3.2.2 on Microsoft.Testing.Platform, **FluentAssertions 7.2.2** (Apache-2.0), NSubstitute 5.3.0, `Microsoft.AspNetCore.Mvc.Testing` 10.0.5 and `Testcontainers.PostgreSql` 4.15.0 with image `pgvector/pgvector:pg17`. | Constitution §4 and SKILL §3 name FluentAssertions, and the conventions allow it when pinned to `[7,8)`. `global.json` sits at the **repo root** because the pipeline runs `dotnet test api/` from the root. Verified: that works in MTP mode. |
| D15 | Test config injection | `ApiFactory` uses `UseEnvironment("Testing")` and `ConfigureAppConfiguration(... AddInMemoryCollection)`. No `RemoveAll<DbContextOptions>`. | `Program.cs` reads the connection string lazily inside the `AddDbContext` lambda. In-memory config is added last, so it beats a developer's local `appsettings.json`. |
| D16 | One container per test run | xUnit v3 `[assembly: AssemblyFixture(typeof(ApiFactory))]`. | A single PostgreSQL container, shared by all integration classes. Tests use unique data per test. |
| D17 | Vendored security fixes | (a) Replace `Core.Exceptions/ExceptionMiddleware.cs` with the template version (camelCase JSON; 4xx logged at Warning). (b) `ErrorResponseHandler` puts the exception message and stack trace in `Data` **only in Development**. | SKILL §8.6, §8.7 and §10 ("Error leakage") are BLOCKING rules. The template already fixed (a). (b) is still open in both copies. |
| D18 | Folder for `User`/`Role` | `Elmanhg.Domain/Identity/` | SKILL §3 says so. Morabh's `Users/` and `Roles/` are not used. |
| D19 | Ports | `http://localhost:5080` and `https://localhost:7080`; `launchUrl` is `scalar/v1`. | Any free pair. The Postman environment uses the http one. |
| D20 | Default culture | `CoreLocalization:DefaultLanguage = "ar"` | PRD §14: the UI is Arabic. |
| D21 | Migrations pipeline | Locally: `dotnet ef database update` (no `Migrate()` at startup, per SKILL §6.7). In CI: `dotnet ef migrations has-pending-model-changes` as the gate, and an idempotent script uploaded as an artifact. | Catches a forgotten migration before merge. |

## Existing code touched
| File | Change |
|------|--------|
| `D:\Personal\elmanhg\.gitignore` | Under `# .NET`, append these lines: `/api/Elmanhg.Api/appsettings.json`, `/api/Elmanhg.Api/appsettings.Development.json`, `/api/Elmanhg.Api/appsettings.Production.json`, `[Ll]ogs/`, `artifacts/`. Do not touch any other line. `.env` is already ignored, and `.env.example` must stay tracked. |
| `D:\Personal\elmanhg\README.md` | Add two rows to the Folders table: `api/` ("ASP.NET Core 10 backend: `Elmanhg.slnx`, DDD/CQRS on `core-libraries/` vendored from Morabh `Core/`, with SQL Server swapped for PostgreSQL/Npgsql") and `postman/` ("Postman collection mirroring the API surface"). Add a section `## Run the backend locally` containing exactly these commands: `cp .env.example .env`, `cp api/Elmanhg.Api/appsettings.example.json api/Elmanhg.Api/appsettings.json`, `docker compose up -d postgres`, `dotnet tool restore`, `dotnet ef database update --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api`, `dotnet run --project api/Elmanhg.Api --launch-profile http`, then `curl http://localhost:5080/health` → `Healthy`. Add a note that the docs are at `http://localhost:5080/scalar/v1` and that tests run with `dotnet test api/` (Docker required). |

## Files to create

### A. Repo root
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `global.json` | JSON | `{ "sdk": { "version": "10.0.401", "rollForward": "latestFeature" }, "test": { "runner": "Microsoft.Testing.Platform" } }` |
| A2 | `.config/dotnet-tools.json` | JSON | `{"version":1,"isRoot":true,"tools":{"dotnet-ef":{"version":"10.0.5","commands":["dotnet-ef"]}}}` |
| A3 | `docker-compose.yml` | YAML | See below. |
| A4 | `.env.example` | env | See below. |
| A5 | `.github/workflows/api-ci.yml` | YAML | See below. |
| A6 | `postman/elmanhg.postman_collection.json` | JSON | Postman v2.1. `info.name` is `"Elmanhg"`. `info.description` is `"Mirrors the API surface. Every endpoint added, changed, or removed updates this collection in the same change."`. Collection variables: `baseUrl` = `http://localhost:5080` and `accessToken` = `""`. Collection auth is bearer `{{accessToken}}`. One item, `"Health"`: `GET {{baseUrl}}/health` with `auth: { "type": "noauth" }` and a `test` event script `pm.test("status is 200", function () { pm.response.to.have.status(200); }); pm.test("body is Healthy", function () { pm.expect(pm.response.text()).to.eql("Healthy"); });`. Shape as in `D:\Personal\CoreLibrary\dotnet-templates\solution\postman\AppTemplate.postman_collection.json`. |
| A7 | `postman/local.postman_environment.json` | JSON | `{"name":"Elmanhg Local","values":[{"key":"baseUrl","value":"http://localhost:5080","enabled":true},{"key":"accessToken","value":"","enabled":true}],"_postman_variable_scope":"environment"}` |

**A3 `docker-compose.yml`:**
```yaml
services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: elmanhg-postgres
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-elmanhg}
      POSTGRES_USER: ${POSTGRES_USER:-elmanhg}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?set POSTGRES_PASSWORD in .env}
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    volumes:
      - elmanhg-postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 10
volumes:
  elmanhg-postgres-data:
```

**A4 `.env.example`** (placeholders only, no real secret):
```
# Copy to .env (gitignored). Read by docker compose and, in Development, by Elmanhg.Api.
POSTGRES_DB=elmanhg
POSTGRES_USER=elmanhg
POSTGRES_PASSWORD=change-me-local-only
POSTGRES_PORT=5432
ConnectionStrings__DbConnectionString='Host=localhost;Port=5432;Database=elmanhg;Username=elmanhg;Password=change-me-local-only'
CoreJwt__Issuer=Elmanhg
CoreJwt__Audience=Elmanhg.Audience
CoreJwt__Key=change-me-generate-a-unique-signing-key-of-at-least-32-bytes
```

**A5 `.github/workflows/api-ci.yml`:**
```yaml
name: api-ci
on:
  pull_request:
    paths: ['api/**', 'global.json', '.config/**', '.github/workflows/api-ci.yml']
  push:
    branches: [main]
    paths: ['api/**', 'global.json', '.config/**', '.github/workflows/api-ci.yml']
permissions:
  contents: read
jobs:
  build-test:
    runs-on: ubuntu-latest
    env:
      DOTNET_NOLOGO: true
      DOTNET_CLI_TELEMETRY_OPTOUT: true
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json
      - name: Restore
        run: dotnet restore api/
      - name: Build
        run: dotnet build api/ -c Release --no-restore
      - name: Test
        run: dotnet test api/ -c Release --no-build
      - name: Vulnerable packages
        run: dotnet list api/ package --vulnerable --include-transitive 2>&1 | tee vulnerable.txt && ! grep -q 'has the following vulnerable' vulnerable.txt
      - name: Restore tools
        run: dotnet tool restore
      - name: Migrations are up to date
        env:
          ConnectionStrings__DbConnectionString: Host=localhost;Database=elmanhg_design;Username=design
        run: dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build
      - name: Idempotent migration script
        env:
          ConnectionStrings__DbConnectionString: Host=localhost;Database=elmanhg_design;Username=design
        run: dotnet ef migrations script --idempotent --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build --output artifacts/migrations.sql
      - uses: actions/upload-artifact@v4
        with:
          name: migrations-sql
          path: artifacts/migrations.sql
```

### B. `api/` solution plumbing
| # | Path | Type | Contract |
|---|------|------|----------|
| B1 | `api/Elmanhg.slnx` | slnx | `<Solution>` with folder `/core-libraries/` holding the 15 projects `core-libraries/Core.{Auditing,Cache,CQRS,DDD,EntityFrameworkCore,Errors,Exceptions,Identity,Localization,Logging,Notifications,OTP,Queues,Utilities,Validation}/Core.X.csproj`. Folder `/Solution Items/` holds the files `Directory.Build.props` and `Directory.Packages.props`. It also lists the projects `Elmanhg.Api/Elmanhg.Api.csproj`, `Elmanhg.Application/Elmanhg.Application.csproj`, `Elmanhg.Domain/Elmanhg.Domain.csproj`, `Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` and `Elmanhg.Tests/Elmanhg.Tests.csproj`. It must be the **only** `.sln`/`.slnx`/`.csproj` directly in `api/`, because `dotnet build api/` resolves it. |
| B2 | `api/Directory.Build.props` | MSBuild | `PropertyGroup`: `TargetFramework=net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`, `WarningsNotAsErrors=$(WarningsNotAsErrors);NU1901;NU1902;NU1903;NU1904`. Nothing else: no Sonar, no `NoWarn`. |
| B3 | `api/Directory.Packages.props` | MSBuild | `ManagePackageVersionsCentrally=true`. `PackageVersion` items: `MediatR` 14.1.0; `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.5; `Microsoft.AspNetCore.OpenApi` 10.0.9; `Microsoft.OpenApi` 2.7.5 (XML comment: `direct pin — AspNetCore.OpenApi 10.0.9 pulls vulnerable 2.0.0, GHSA-v5pm-xwqc-g5wc`); `Microsoft.EntityFrameworkCore.Design` 10.0.5; `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.5; `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3; `Scalar.AspNetCore` 2.13.14; `DotNetEnv` 3.1.1. Under Label `Testing`: `xunit.v3.mtp-v2` 3.2.2; `FluentAssertions` 7.2.2 (comment: `pinned <8 — v8+ is commercial`); `NSubstitute` 5.3.0; `Microsoft.AspNetCore.Mvc.Testing` 10.0.5; `Testcontainers.PostgreSql` 4.15.0. |
| B4 | `api/core-libraries/Directory.Build.props` | MSBuild | `<Project>` with only an XML comment: `Stops MSBuild walking up to api/Directory.Build.props: vendored Core keeps its own csproj settings and is not held to TreatWarningsAsErrors.` |
| B5 | `api/core-libraries/Directory.Packages.props` | MSBuild | `<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>`, plus the comment `Core pins versions inline, verbatim from Morabh.` |

### C. `api/core-libraries/` — vendored tree
**C0. Copy.** Copy `D:\Personal\Projects\Projects\Morabh\repos\apis\Core\*` to `api/core-libraries\`:
- Exclude every `bin/` and `obj/`.
- Exclude the whole `Core.Azure/` folder.
- Result: 15 project folders. Namespaces stay `Core.*`, unchanged.

Then apply exactly these edits and no others:

| # | File (under `api/core-libraries/`) | Edit |
|---|------|------|
| C1 | `Core.Logging/Core.Logging.csproj` | Delete the `Serilog.Sinks.MSSqlServer` PackageReference. |
| C2 | `Core.Logging/DependencyInjection.cs` | Delete `using Serilog.Sinks.MSSqlServer;` and `using System.Data;`. Delete the line `ConfigureSql(loggerConfig, options.Sql);`. Delete the whole `// SQL` `ConfigureSql` method. |
| C3 | `Core.Logging/LoggingOptions.cs` | Delete the property `public SqlLoggingOptions Sql { get; set; } = new();`, the class `SqlLoggingOptions` and the class `SqlSinkTarget`. |
| C4 | `Core.DDD/Entities/AuditEntity.cs` | `CreationDate` and `UpdationDate` initialisers: `DateTimeOffset.Now` → `DateTimeOffset.UtcNow`. |
| C5 | `Core.EntityFrameworkCore/Repositories/Repository.cs` | In `SaveChangesAsync`: both `DateTimeOffset.Now` → `DateTimeOffset.UtcNow`. |
| C6 | `Core.EntityFrameworkCore/Context/CoreDbContext.cs` | In the `Notification` builder, chain `.HasColumnType("jsonb")` after the existing `.HasConversion(...)` on `x.Data`. Leave the converter as is. |
| C7 | `Core.Exceptions/ExceptionMiddleware.cs` | Replace the whole file with `D:\Personal\CoreLibrary\dotnet-templates\solution\Core\Exceptions\ExceptionMiddleware.cs` (static `ErrorSerializerOptions = new(JsonSerializerDefaults.Web)`; 4xx → `LogWarning`, else `LogError`). |
| C8 | `Core.Exceptions/ErrorResponseHandler.cs` | Change the primary constructor to `public sealed class ErrorResponseHandler(ILocalizer localizer, IHostEnvironment hostEnvironment) : IErrorResponseHandler` and add `using Microsoft.Extensions.Hosting;`. In `GenerateErrorResponse(ExceptionDetails)`, set `Data = hostEnvironment.IsDevelopment() ? $"{exceptionDetails.Exception?.Message} - {exceptionDetails.Exception?.StackTrace}" : null`. Leave the other two methods unchanged. |

### D. `Elmanhg.Domain`
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/Elmanhg.Domain.csproj` | csproj | `Microsoft.NET.Sdk`. `PackageReference Microsoft.AspNetCore.Identity.EntityFrameworkCore` (no Version). `ProjectReference ..\core-libraries\Core.DDD\Core.DDD.csproj`. Source: `Morabh.Domain.csproj`. |
| D2 | `api/Elmanhg.Domain/Identity/User.cs` | class | `namespace Elmanhg.Domain.Identity;` `public class User : IdentityUser<Guid>, IAuditEntity` with exactly these members: `public Guid? CreatedBy { get; set; }`, `public DateTimeOffset CreationDate { get; set; } = DateTimeOffset.UtcNow;`, `public Guid? UpdatedBy { get; set; }`, `public DateTimeOffset UpdationDate { get; set; } = DateTimeOffset.UtcNow;`, `public bool IsDeleted { get; set; }`, `public DateTimeOffset? DeletedAt { get; set; }`. The implicit public parameterless constructor is required by the `new()` constraint. No factory: #56 adds `Create`. Source: `Morabh.Domain/Users/User.cs` and template `Domain/Identity/User.cs`, reduced to the base plumbing. |
| D3 | `api/Elmanhg.Domain/Identity/Role.cs` | class | `namespace Elmanhg.Domain.Identity;` `public class Role : IdentityRole<Guid>` with an empty body `{ }`. Source: `Morabh.Domain/Roles/Role.cs`. |

### E. `Elmanhg.Application`
| # | Path | Type | Contract |
|---|------|------|----------|
| E1 | `api/Elmanhg.Application/Elmanhg.Application.csproj` | csproj | `PackageReference MediatR`. ProjectReferences: `..\core-libraries\Core.Auditing`, `Core.CQRS`, `Core.Identity`, `Core.Localization`, `Core.Utilities`, `Core.Validation` (each `...\Core.X\Core.X.csproj`), and `..\Elmanhg.Domain\Elmanhg.Domain.csproj`. |
| E2 | `api/Elmanhg.Application/DependencyInjection.cs` | static class | `namespace Elmanhg.Application;` `public static class DependencyInjection`. It has `public static IServiceCollection AddApplication(this IServiceCollection services)`, whose body is `services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));`, then `services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());`, then `return services;`. Usings: `FluentValidation`, `Microsoft.Extensions.DependencyInjection`, `System.Reflection`. Source: `Morabh.Application/DependencyInjection.cs`, minus the configuration parameter (SKILL §5.11). |

### F. `Elmanhg.Infrastructure`
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | csproj | PackageReferences: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`; `Microsoft.EntityFrameworkCore.Design` with `<PrivateAssets>all</PrivateAssets><IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>`; `Npgsql.EntityFrameworkCore.PostgreSQL`. ProjectReferences: `..\core-libraries\Core.EntityFrameworkCore\Core.EntityFrameworkCore.csproj`, `..\Elmanhg.Application\Elmanhg.Application.csproj`, `..\Elmanhg.Domain\Elmanhg.Domain.csproj`. Source: `Morabh.Infrastructure.csproj`, with SqlServer → Npgsql and no `.Tools`. |
| F2 | `api/Elmanhg.Infrastructure/DependencyInjection.cs` | static class | `namespace Elmanhg.Infrastructure;` `public static IServiceCollection AddInfrastructure(this IServiceCollection services)` whose body is `return services;` only. `IRepository<>` is already registered by `AddCoreEntityFrameworkCore`. |
| F3 | `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | class | See below. |
| F4 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_InitialCreate.cs`, `<timestamp>_InitialCreate.Designer.cs`, `AppDbContextModelSnapshot.cs` | generated | Generate from the repo root with `dotnet tool restore`, then `dotnet ef migrations add InitialCreate --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --output-dir Migrations`. Do not edit by hand. Open the file and check: `Notifications.Data` has `type: "jsonb"`; every `DateTimeOffset` is `timestamp with time zone`; there is no `nvarchar`/`datetime2`; there are no Drop or Rename operations. |

**F3 `AppDbContext`** (source: `Morabh.Infrastructure/Data/Context/AppDbContext.cs` / template):
```csharp
using Core.EntityFrameworkCore.Context;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Data.Context;

public class AppDbContext(DbContextOptions options, IMediator mediator) : CoreDbContext<User, Role, Guid>(options, mediator)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(modelBuilder);
    }

    private static void ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasQueryFilter(x => !x.IsDeleted);
    }
}
```
It has no `OnConfiguring` and no DbSets beyond those inherited.

### G. `Elmanhg.Api`
| # | Path | Type | Contract |
|---|------|------|----------|
| G1 | `api/Elmanhg.Api/Elmanhg.Api.csproj` | csproj | `Microsoft.NET.Sdk.Web`. No `UserSecretsId`, no `NoWarn`. PackageReferences: `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi`, `Microsoft.EntityFrameworkCore.Design` (PrivateAssets `all`, same IncludeAssets as F1), `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`, `Scalar.AspNetCore`, `DotNetEnv`. ProjectReferences to `..\core-libraries\Core.{Auditing,CQRS,EntityFrameworkCore,Exceptions,Identity,Localization,Logging,Utilities}\Core.X.csproj` and to `..\Elmanhg.Application`, `..\Elmanhg.Domain`, `..\Elmanhg.Infrastructure`. |
| G2 | `api/Elmanhg.Api/Program.cs` | top-level | See below. **Pipeline order is fixed**, mirrored from `Morabh.APIs/Program.cs`. |
| G3 | `api/Elmanhg.Api/Properties/launchSettings.json` | JSON | Profiles `http` (`applicationUrl` `http://localhost:5080`) and `https` (`https://localhost:7080;http://localhost:5080`). Both: `commandName: Project`, `launchBrowser: true`, `launchUrl: "scalar/v1"`, `ASPNETCORE_ENVIRONMENT=Development`. Shape as Morabh's. |
| G4 | `api/Elmanhg.Api/appsettings.example.json` | JSON | See below. |
| G5 | `api/Elmanhg.Api/Resources/Messages.en.resx` | resx | Header copied verbatim from `Morabh.APIs/Resources/Messages.en.resx` (schema and resheaders, everything before the first `<data>`). Two entries: `UNHANDLED_EXCEPTION` = `An unexpected error occurred. Please try again later.` and `VALIDATION_FAILED` = `One or more validation errors occurred.` |
| G6 | `api/Elmanhg.Api/Resources/Messages.ar.resx` | resx | Same header. `UNHANDLED_EXCEPTION` = `حدث خطأ غير متوقع. برجاء المحاولة لاحقا.` and `VALIDATION_FAILED` = `حدث خطأ أو أكثر في التحقق من البيانات.` (copied from Morabh `Messages.ar.resx` lines 263–264). |
| local-only | `api/Elmanhg.Api/appsettings.json` | JSON | A copy of G4, **gitignored and never committed** (constitution §2). The implementer creates it only so `dotnet run` works locally. |

**G2 `Program.cs`** — write exactly this composition. Blank lines and `#region` markers are as in Morabh.
```csharp
using Core.Auditing;
using Core.CQRS;
using Core.EntityFrameworkCore;
using Core.Exceptions;
using Core.Identity;
using Core.Localization;
using Core.Logging;
using Core.Utilities;
using DotNetEnv;
using Elmanhg.Application;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

#region LOCAL ENVIRONMENT
if (builder.Environment.IsDevelopment())
{
    Env.TraversePath().Load();
    builder.Configuration.AddEnvironmentVariables();
}
#endregion

builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

#region IDENTITY
builder.Services.AddCoreIdentity<User, Guid, Role, AppDbContext>(configuration: builder.Configuration, dbContextOptions: options => options.UseNpgsql(builder.Configuration.GetConnectionString("DbConnectionString"), npgsql => npgsql.EnableRetryOnFailure()));
#endregion

#region CORE SERVICES
builder.Services.AddCoreLogging(builder.Configuration, builder.Environment);
builder.Services.AddCoreLocalization();
builder.Services.AddCoreExceptions();
// Auditing before CQRS so AuditBehaviour sits outermost in the MediatR pipeline.
builder.Services.AddCoreAuditing(builder.Configuration);
builder.Services.AddCoreCQRS();
builder.Services.AddCoreEntityFrameworkCore<User, Role, Guid, AppDbContext>();
builder.Services.AddCoreUtilities();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
#endregion

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// First, so every downstream middleware and handler resolves in the caller's language.
app.UseCoreLocalization(builder.Configuration);

app.UseMiddleware<CoreRequestLoggingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.UseMiddleware<CoreExceptionMiddleware>();

app.MapControllers();

app.MapHealthChecks("/health").AllowAnonymous();

try
{
    await app.RunAsync();
}
finally
{
    // Flush buffered sinks so the last batch reaches the log store on shutdown.
    await Serilog.Log.CloseAndFlushAsync();
}

public partial class Program;
```
Relative to Morabh, this drops the Azure App Configuration region, `AddUserSecrets`, `AddCoreAzure`, `AddCoreOtp`, `AddCoreNotifications`, all `MapCore*Endpoints`, `identityOptions` and `ConfigureHttpJsonOptions` (replaced by the MVC `AddJsonOptions`, SKILL §8.6).

**G4 `appsettings.example.json`** (shape plus safe defaults; secrets empty):
```json
{
  "ConnectionStrings": { "DbConnectionString": "" },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "CoreLocalization": { "DefaultLanguage": "ar" },
  "CoreLogging": {
    "Trace": { "ServiceName": "Elmanhg", "Cluster": "Local", "Version": "1.0.0" },
    "Console": { "Enabled": true, "MinimumLevel": "Information", "Environments": [] },
    "File": { "Enabled": false, "MinimumLevel": "Information", "BasePath": "Logs", "RetentionInDays": 30 },
    "Seq": { "Enabled": false, "ServerUrl": "http://localhost:5341", "ApiKey": "", "MinimumLevel": "Information" },
    "AzureTable": { "Enabled": false, "ConnectionString": "", "MinimumLevel": "Information", "LogsTableName": "Logs", "RequestLogsTableName": "RequestLogs" },
    "AppInsights": { "Enabled": false, "ConnectionString": "", "MinimumLevel": "Warning" }
  },
  "CoreAuditing": { "Enabled": false },
  "CoreJwt": { "Issuer": "", "Audience": "", "Key": "", "ExpirationHours": 72, "RefreshTokenExpirationDays": 7, "RefreshTokenLength": 64 }
}
```

### H. `Elmanhg.Tests`
| # | Path | Type | Contract |
|---|------|------|----------|
| H1 | `api/Elmanhg.Tests/Elmanhg.Tests.csproj` | csproj | `Microsoft.NET.Sdk`. Properties: `OutputType=Exe`, `IsPackable=false`, `IsTestProject=true`. PackageReferences: `xunit.v3.mtp-v2`, `FluentAssertions`, `NSubstitute`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`. `<ItemGroup><Using Include="Xunit" /></ItemGroup>`. ProjectReferences: `..\Elmanhg.Api`, `..\Elmanhg.Application`, `..\Elmanhg.Domain`, `..\Elmanhg.Infrastructure` (each `.csproj`). |
| H2 | `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | fixture | See below. |
| H3 | `api/Elmanhg.Tests/Integration/Health/HealthEndpointTests.cs` | tests | `namespace Elmanhg.Tests.Integration.Health;` `public sealed class HealthEndpointTests(ApiFactory factory)`. Tests T1–T2. |
| H4 | `api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs` | tests | `public sealed class OpenApiEndpointTests(ApiFactory factory)`. Tests T3–T4. |
| H5 | `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | tests | `public sealed class AppDbContextTests(ApiFactory factory)`. Tests T5–T8. Resolve `AppDbContext` from `factory.Services.CreateScope()`, with a new scope for every read-back. |
| H6 | `api/Elmanhg.Tests/Integration/Composition/PipelineCompositionTests.cs` | tests | `public sealed class PipelineCompositionTests(ApiFactory factory)`. Also declares, in the same file, `public sealed record PipelineProbeRequest : IRequest<Unit>;` as the probe type. Test T9. |
| H7 | `api/Elmanhg.Tests/Core/Exceptions/CoreExceptionMiddlewareTests.cs` | tests | Unit, no doubles for the SUT. Build `DefaultHttpContext`: `Response.Body = new MemoryStream()`, and `RequestServices` = `new ServiceCollection().AddSingleton<IErrorResponseHandler>(new ErrorResponseHandler(localizer, hostEnvironment)).BuildServiceProvider()`. `localizer` is `Substitute.For<ILocalizer>()`, and `GetMessage(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Dictionary<string, object>?>())` returns argument 1 `?? string.Empty`. `hostEnvironment` is `Substitute.For<IHostEnvironment>()` with `EnvironmentName` = `"Production"`. The logger is `NullLogger<CoreExceptionMiddleware>.Instance`. Tests T10–T12. |
| H8 | `api/Elmanhg.Tests/Core/Exceptions/ErrorResponseHandlerTests.cs` | tests | Unit tests with the same `localizer` and `hostEnvironment` substitutes. Input is `new ExceptionDetails { Code = "PROBE_FAILED", Message = "probe", StatusCode = 500, Exception = new InvalidOperationException("probe-detail") }`. Tests T13–T14. |

**H2 `ApiFactory`:**
```csharp
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Elmanhg.Tests.Integration.Infrastructure.ApiFactory))]

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string PostgresImage = "pgvector/pgvector:pg17";
    private const string TestingEnvironment = "Testing";
    // Signs nothing outside this in-memory host; the JwtBearer options delegate only requires it to be non-empty.
    private const string TestJwtKey = "elmanhg-tests-signing-key-not-a-secret-0123456789";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(PostgresImage).Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestingEnvironment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DbConnectionString"] = _database.GetConnectionString(),
            ["CoreJwt:Issuer"] = "Elmanhg.Tests",
            ["CoreJwt:Audience"] = "Elmanhg.Tests",
            ["CoreJwt:Key"] = TestJwtKey,
            ["CoreAuditing:Enabled"] = "false",
        }));
    }

    public new async ValueTask DisposeAsync()
    {
        await _database.DisposeAsync();
        await base.DisposeAsync();
    }
}
```
If Docker is unavailable, `StartAsync` throws with Docker's message. The implementer reports `BLOCKED: docker unavailable` and never skips.

## Error codes
No new `ErrorCodes` constants. The two Core codes that can now reach a client get resource strings:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ExceptionErrorCodes.UnhandledException` (Core.Exceptions) | `UNHANDLED_EXCEPTION` | `CoreExceptionMiddleware`, for any non-`BaseException` | — (mapped) | 500 |
| (Core.CQRS `ValidationBehaviourException` default) | `VALIDATION_FAILED` | `ValidationBehaviour` | `ValidationBehaviourException` | 422 |

| Key | English | Arabic |
|-----|---------|--------|
| `UNHANDLED_EXCEPTION` | An unexpected error occurred. Please try again later. | حدث خطأ غير متوقع. برجاء المحاولة لاحقا. |
| `VALIDATION_FAILED` | One or more validation errors occurred. | حدث خطأ أو أكثر في التحقق من البيانات. |

## Domain behaviour
There is no domain behaviour in this slice. `User` and `Role` are Identity plumbing with no methods. The only domain-adjacent change is C4: `AuditEntity` stamps UTC.

## API surface
| Method | Route | Policy | Request | Response |
|--------|-------|--------|---------|----------|
| GET | `/health` | `AllowAnonymous` (a framework health endpoint, not a controller) | — | `200 text/plain "Healthy"` / `503 "Unhealthy"` |
| GET | `/openapi/v1.json` | none; only mapped when not Production | — | `200 application/json` OpenAPI document |
| GET | `/scalar/v1` | none; only mapped when not Production | — | `200 text/html` |

No controllers and no `DefaultCodes` constants: no protected endpoint exists yet.

## Test plan
Every async call passes `TestContext.Current.CancellationToken`. Use Arrange/Act/Assert blocks separated by blank lines.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `HealthEndpointTests` | `Get_AnonymousWithDatabaseUp_Returns200Healthy` | `GET /health` with no auth header returns status `200` and a body exactly `"Healthy"`. |
| T2 | `HealthEndpointTests` | `Get_Health_ReturnsTraceIdHeader` | The response has a non-empty `X-Trace-Id` header. This proves `CoreRequestLoggingMiddleware`, the structured logging, is in the pipeline. |
| T3 | `OpenApiEndpointTests` | `Get_OpenApiDocument_Returns200Json` | `GET /openapi/v1.json` returns `200` with content-type media type `application/json`, and the parsed JSON has a root property `"openapi"`. |
| T4 | `OpenApiEndpointTests` | `Get_ScalarReference_Returns200Html` | `GET /scalar/v1` returns `200` with media type `text/html`. |
| T5 | `AppDbContextTests` | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | `GetPendingMigrationsAsync()` is empty, and `GetAppliedMigrationsAsync()` has exactly one entry ending with `"_InitialCreate"`. |
| T6 | `AppDbContextTests` | `Model_Current_MatchesLatestMigrationSnapshot` | `context.Database.HasPendingModelChanges()` is `false`. |
| T7 | `AppDbContextTests` | `Migrate_NotificationData_CreatesJsonbColumn` | `context.Database.SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = {"Notifications"} AND column_name = {"Data"}").SingleAsync(...)` equals `"jsonb"`. |
| T8 | `AppDbContextTests` | `SaveChangesAsync_CoreAuditEntity_PersistsUtcTimestamps` | Create `NotificationTemplate.Create(Guid.CreateVersion7(), $"probe-{Guid.CreateVersion7():N}", new LocalizedText("عنوان", "Title"), new LocalizedText("محتوى", "Content"), null)`. Add it with `context.NotificationTemplates.Add` and call `SaveChangesAsync`. Read it back in a **new scope** with `AsNoTracking().SingleAsync(x => x.Id == id)`. Assert that `Code` equals the created code, `Title.English == "Title"`, and `CreationDate.Offset == TimeSpan.Zero`. |
| T9 | `PipelineCompositionTests` | `Resolve_PipelineBehaviours_IncludesValidationBehaviour` | In a new scope, `GetServices<IPipelineBehavior<PipelineProbeRequest, Unit>>()` contains an instance of type `ValidationBehaviour<PipelineProbeRequest, Unit>`. |
| T10 | `CoreExceptionMiddlewareTests` | `InvokeAsync_CoreExceptionThrown_WritesStatusAndCamelCaseCode` | `next` throws `new NotFoundCoreException("PROBE_NOT_FOUND")`. Assert `Response.StatusCode == 404`, and that the parsed body has a **lower-case** property `code` (`JsonDocument.RootElement.GetProperty("code")`) equal to `"PROBE_NOT_FOUND"`. |
| T11 | `CoreExceptionMiddlewareTests` | `InvokeAsync_UnhandledExceptionThrown_Returns500WithUnhandledCode` | `next` throws `new InvalidOperationException("boom")`. Assert status `500`, body `code == "UNHANDLED_EXCEPTION"`, and body `data` is a JSON null (Production environment). |
| T12 | `CoreExceptionMiddlewareTests` | `InvokeAsync_NoException_LeavesResponseUntouched` | `next` completes. Assert status stays `200` and `Response.Body.Length == 0`. |
| T13 | `ErrorResponseHandlerTests` | `GenerateErrorResponse_DevelopmentEnvironment_IncludesExceptionDetail` | With `EnvironmentName = "Development"`, the result's `Data` contains `"probe-detail"`, and `Code == "PROBE_FAILED"`. |
| T14 | `ErrorResponseHandlerTests` | `GenerateErrorResponse_ProductionEnvironment_OmitsExceptionDetail` | With `EnvironmentName = "Production"`, the result's `Data` is `null`. |

## Definition of done
- [ ] `api/core-libraries/` holds 15 `Core.*` projects copied from Morabh, with no `Core.Azure` and no `bin/`/`obj/`. Edits are only C1–C8.
- [ ] `grep -rniE "SqlServer|MSSql|UseSqlServer" api/` prints nothing.
- [ ] `grep -rn "DateTimeOffset.Now" api/` prints nothing.
- [ ] `api/Elmanhg.slnx` lists exactly the 15 core projects plus the 5 `Elmanhg.*` projects, and is the only solution/project file directly in `api/`.
- [ ] CPM is on for `Elmanhg.*` and every `Elmanhg.*` `PackageReference` has no `Version`. `core-libraries` has CPM off via B5 and does not inherit B2 because of B4.
- [ ] No `NoWarn` for NU1901–NU1904 anywhere in `api/`, and no `UserSecretsId`.
- [ ] `Program.cs` matches G2 exactly: order, the `!IsProduction()` docs gate, `/health` anonymous, `public partial class Program;`. No `Migrate()`/`EnsureCreated()` call anywhere in production code.
- [ ] `AppDbContext` matches F3. `InitialCreate` is generated by the EF tool. `Notifications.Data` is `jsonb`, and there are no SQL Server column types.
- [ ] `api/Elmanhg.Api/appsettings.json` is **not** tracked and is gitignored. `appsettings.example.json` and `.env.example` are tracked and contain no real secret.
- [ ] `docker compose up -d postgres` starts `pgvector/pgvector:pg17` and reaches healthy.
- [ ] With `.env` and `appsettings.json` created from the examples, `dotnet ef database update` succeeds, `dotnet run --project api/Elmanhg.Api --launch-profile http` starts, and `curl http://localhost:5080/health` returns `Healthy`.
- [ ] `dotnet build api/` succeeds with zero warnings in `Elmanhg.*` projects.
- [ ] `dotnet test api/` passes all 14 tests (T1–T14), with Docker running.
- [ ] `dotnet list api/ package --vulnerable --include-transitive` reports no vulnerable packages.
- [ ] `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api` exits 0.
- [ ] `.github/workflows/api-ci.yml` matches A5: restore, build, test, vulnerable gate, tool restore, pending-model-changes, idempotent script artifact.
- [ ] `postman/elmanhg.postman_collection.json` has the anonymous `GET {{baseUrl}}/health` request with its two test assertions. `postman/local.postman_environment.json` exists.
- [ ] `global.json` is at the repo root with the MTP test runner. `.config/dotnet-tools.json` pins `dotnet-ef` 10.0.5.
- [ ] README has the `api/` and `postman/` rows and the run section. No doc is created outside `/docs` or the root README.
- [ ] Every Elmanhg `.cs` file uses file-scoped namespaces, has no comments except the WHY comments given above, and uses `DateTimeOffset` only.
